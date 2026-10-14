package com.teamdoodle.auth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.os.Handler;
import android.util.Log;
import android.os.Looper;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CredentialOption;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;

/**
 * Credential Manager의 "Google 계정으로 로그인" 창을 띄워 Google ID 토큰을 받는다.
 * Unity(C#)의 AndroidGoogleSignIn이 signIn을 부르고, 결과는 Callback으로 돌려준다.
 * 콜백은 Android 메인 스레드에서 불리므로 C# 쪽에서 Unity 메인 스레드로 옮겨 쓴다.
 */
public final class GoogleSignInBridge {
    /** 로그인 결과. 셋 중 하나만 한 번 불린다 */
    public interface Callback {
        void onSuccess(String idToken);

        /** 사용자가 창을 닫았다 */
        void onCancel();

        /** code: NO_CREDENTIAL(기기에 Google 계정 없음), UNEXPECTED_CREDENTIAL, 그 밖은 Credential Manager 예외 타입 */
        void onError(String code, String message);
    }

    private GoogleSignInBridge() {
    }

    private static final String TAG = "GoogleSignIn";

    /** 계정 선택창을 보여 주는 로그인 (버튼을 눌렀을 때) */
    public static void signIn(Activity activity, String serverClientId, Callback callback) {
        signIn(activity, serverClientId, false, callback);
    }

    /**
     * @param serverClientId Google Cloud의 "웹 애플리케이션" OAuth 클라이언트 ID. ID 토큰의 aud가 이 값이 되고, 서버가 이 값으로 검증한다
     * @param silent 앱을 켤 때의 자동 로그인. 이전에 이 앱에 로그인한 계정이 있으면 선택창 없이 그 계정으로 로그인한다.
     *     그런 계정이 없으면(NO_CREDENTIAL) 계정 선택창을 보여 주는 로그인으로 넘어간다
     */
    public static void signIn(Activity activity, String serverClientId, boolean silent, Callback callback) {
        CredentialOption option = silent
                ? new GetGoogleIdOption.Builder()
                        .setServerClientId(serverClientId)
                        .setFilterByAuthorizedAccounts(true)
                        .setAutoSelectEnabled(true)
                        .build()
                : new GetSignInWithGoogleOption.Builder(serverClientId).build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();
        Handler main = new Handler(Looper.getMainLooper());
        Executor executor = main::post;

        CredentialManager.create(activity).getCredentialAsync(
                activity,
                request,
                new CancellationSignal(),
                executor,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        Credential credential = response.getCredential();
                        if (credential instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                            try {
                                callback.onSuccess(GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken());
                            } catch (Exception e) {
                                Log.w(TAG, "ID 토큰을 읽지 못했다: " + e.getMessage());
                                callback.onError("UNEXPECTED_CREDENTIAL", e.getMessage());
                            }
                            return;
                        }

                        Log.w(TAG, "예상하지 못한 자격 증명 종류: " + credential.getType());
                        callback.onError("UNEXPECTED_CREDENTIAL", credential.getType());
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        // 자동 로그인에 쓸 계정이 없으면 계정 선택창으로 넘어간다
                        if (silent && e instanceof NoCredentialException) {
                            Log.i(TAG, "자동 로그인할 계정이 없어 계정 선택창으로 넘어간다");
                            signIn(activity, serverClientId, false, callback);
                            return;
                        }

                        // 취소와 설정 오류(패키지명·SHA-1 불일치 등)가 같은 취소로 올 수 있어 원인을 남긴다
                        Log.w(TAG, "로그인 실패 silent=" + silent + " type=" + e.getType() + " message=" + e.getMessage());
                        if (e instanceof GetCredentialCancellationException) {
                            callback.onCancel();
                        } else if (e instanceof NoCredentialException) {
                            callback.onError("NO_CREDENTIAL", e.getMessage());
                        } else {
                            callback.onError(e.getType(), e.getMessage());
                        }
                    }
                });
    }}
