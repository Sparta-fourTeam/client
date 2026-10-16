# Unity calls these APIs through JNI; keep public methods and Task implementations available.
-keep class com.google.android.play.core.appupdate.** { *; }
-keep class com.google.android.play.core.install.model.UpdateAvailability { *; }
-keep class com.google.android.gms.tasks.** { *; }
