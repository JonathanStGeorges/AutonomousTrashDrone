using System;
using UnityEngine;

namespace TrashSailDemo
{
    public static class DemoInferenceProtocol
    {
        public static bool Matches(InferenceResult result, string session, CameraFrame frame, string profile)
        {
            return result != null && result.ok && result.sessionId == session && result.cameraId == "onboard" &&
                result.sequence == frame.Sequence && Math.Abs(result.captureTime-frame.CaptureTime)<0.00001 &&
                result.width==frame.Width && result.height==frame.Height && result.profile==profile && result.detections!=null;
        }
        public static Rect BottomLeftBox(ModelEvidence item, int width, int height)
        {
            if(item?.box==null || item.box.Length!=4) return Rect.zero;
            float x=Mathf.Clamp(item.box[0],0,width), y=Mathf.Clamp(item.box[1],0,height);
            return new Rect(x,height-Mathf.Clamp(item.box[3],0,height),Mathf.Max(0,Mathf.Min(width,item.box[2])-x),Mathf.Max(0,Mathf.Min(height,item.box[3])-y));
        }
        public static Vector2 WaterContact(ModelEvidence item, Rect box, int height)
        {
            // Lowest segmentation edge estimates the contact, never the box centre.
            if(item.polygon!=null && item.polygon.Length>=6)
            {
                float bottom=float.NegativeInfinity;
                for(int i=1;i<item.polygon.Length;i+=2) bottom=Mathf.Max(bottom,item.polygon[i]);
                float x=0;int count=0;
                for(int i=0;i+1<item.polygon.Length;i+=2)
                    if(item.polygon[i+1]>=bottom-1) {x+=item.polygon[i];count++;}
                if(count>0) return new Vector2(x/count,height-bottom);
            }
            return new Vector2(box.center.x,box.yMin);
        }
    }
}
