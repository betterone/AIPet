#import <ARKit/ARKit.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>
#include <simd/simd.h>

@interface MomoPositionSession : NSObject <ARSessionDelegate>
@property(nonatomic,strong) ARSession *session;
@property(nonatomic) BOOL wanted;
@property(nonatomic) int status;
@property(nonatomic) simd_float4x4 pose;
@property(nonatomic) unsigned int frameSerial;
@end
@implementation MomoPositionSession
- (void)start {
#if TARGET_OS_SIMULATOR
    self.status=5;return;
#else
    if(![ARWorldTrackingConfiguration isSupported]){self.status=5;return;}
    self.wanted=YES;self.status=1;
    AVAuthorizationStatus auth=[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
    if(auth==AVAuthorizationStatusDenied||auth==AVAuthorizationStatusRestricted){self.status=4;return;}
    if(auth==AVAuthorizationStatusNotDetermined){
        [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted){dispatch_async(dispatch_get_main_queue(),^{if(!self.wanted)return;if(granted)[self run];else self.status=4;});}];
    }else [self run];
#endif
}
- (void)run {
    if(!self.wanted)return;
    if(!self.session){self.session=[ARSession new];self.session.delegate=self;self.session.delegateQueue=dispatch_get_main_queue();}
    ARWorldTrackingConfiguration *config=[ARWorldTrackingConfiguration new];
    config.worldAlignment=ARWorldAlignmentGravity;config.planeDetection=ARPlaneDetectionNone;config.lightEstimationEnabled=NO;
    [self.session runWithConfiguration:config options:ARSessionRunOptionResetTracking|ARSessionRunOptionRemoveExistingAnchors];
}
- (void)session:(ARSession *)session didUpdateFrame:(ARFrame *)frame {
    if(!self.wanted)return;
    self.status=frame.camera.trackingState==ARTrackingStateNormal?2:3;
    self.pose=simd_inverse([frame.camera viewMatrixForOrientation:UIInterfaceOrientationPortrait]);
    self.frameSerial=(self.frameSerial+1)%1000000;
}
- (void)sessionWasInterrupted:(ARSession *)session {self.status=3;}
- (void)sessionInterruptionEnded:(ARSession *)session {self.status=1;if(self.wanted)[self run];}
- (void)session:(ARSession *)session didFailWithError:(NSError *)error {self.status=3;}
@end
static MomoPositionSession *tracker;
extern "C" {
int MomoPositionSupported(){
#if TARGET_OS_SIMULATOR
    return 0;
#else
    return [ARWorldTrackingConfiguration isSupported]?1:0;
#endif
}
void MomoPositionStart(){if(!tracker)tracker=[MomoPositionSession new];[tracker start];}
void MomoPositionStop(){tracker.wanted=NO;[tracker.session pause];tracker.status=0;}
int MomoPositionRead(float *values){
    if(!tracker)return 0;
    simd_float4x4 m=tracker.pose;
    values[0]=m.columns[3].x;values[1]=m.columns[3].y;values[2]=-m.columns[3].z;
    simd_float3 f={-m.columns[2].x,-m.columns[2].y,m.columns[2].z};
    values[3]=atan2f(f.x,f.z)*180/M_PI;values[4]=-asinf(fmaxf(-1,fminf(1,f.y)))*180/M_PI;
    values[5]=(float)tracker.frameSerial;
    if(tracker.status==2&&fabsf(f.y)>.97f)return 3;
    return tracker.status;
}
}
