#import <Foundation/Foundation.h>

// NSBundle owns the returned immutable string; C# immediately copies its UTF-8 representation.
extern "C" const char *NovaClientBuildNumber(void) {
    return [[[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"] UTF8String];
}
