using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace TrashSailDemo
{
    public static class DemoOceanRendering
    {
        public static void Configure(Camera camera,bool scenery=true)
        {
            if(camera==null)return;
            var hd=camera.GetComponent<HDAdditionalCameraData>();if(hd==null)hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode=scenery?HDAdditionalCameraData.ClearColorMode.Sky:HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR=Color.black;hd.volumeLayerMask=1;hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;hd.customRenderingSettings=true;
            foreach(var field in new[]{FrameSettingsField.TransparentObjects,FrameSettingsField.Refraction,FrameSettingsField.Water,FrameSettingsField.Postprocess})
            {hd.renderingPathCustomFrameSettings.SetEnabled(field,scenery);hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
            foreach(var field in new[]{FrameSettingsField.ExposureControl,FrameSettingsField.Tonemapping,FrameSettingsField.ColorGrading})
            {hd.renderingPathCustomFrameSettings.SetEnabled(field,scenery);hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
        }
    }
}
