using System.Collections.Generic;
using UnityEngine;
namespace NicromFPS
{
    // Local finger rotations from the shared FPS Hands avatar's open reference skeleton.
    // Weapon mesh bind poses contain a curled grip and are unsuitable as an open palm pose.
    internal static class LeftHandOpenPose
    {
        static readonly Dictionary<string, Quaternion> rotations = new Dictionary<string, Quaternion>
        {
            { "thumb_L.L", new Quaternion(0.33932593f, 0.14371245f, -0.2418952f, 0.89760315f) },
            { "thumb_L.002.L", new Quaternion(-0.024511421f, 0.0211918f, 0.028899375f, 0.999057f) },
            { "thumb_L.001.L", new Quaternion(-0.027091466f, 0.013844343f, 0.0058215023f, 0.9995201f) },
            { "index_L.L", new Quaternion(-0.03682433f, -0.031360686f, -0.03768503f, 0.99811846f) },
            { "index_L.003.L", new Quaternion(0.010715613f, -0.02528149f, 0.02631463f, 0.9992766f) },
            { "index_L.002.L", new Quaternion(0.0085312445f, -0.008187727f, 0.03059576f, 0.99946195f) },
            { "index_L.001.L", new Quaternion(-0.0034931677f, 0.026940005f, 0.06756671f, 0.9973449f) },
            { "middle_L.L", new Quaternion(-0.034554567f, -0.023503926f, -0.039449114f, 0.9983473f) },
            { "middle_L.003.L", new Quaternion(0.010077957f, -0.018292192f, 0.026197584f, 0.99943864f) },
            { "middle_L.002.L", new Quaternion(0.009136096f, -0.010764723f, 0.026147889f, 0.9995584f) },
            { "middle_L.001.L", new Quaternion(0.016963363f, -0.016789995f, 0.026469424f, 0.9993647f) },
            { "ring_L.L", new Quaternion(-0.025353327f, -0.04919505f, -0.022054233f, 0.99822384f) },
            { "ring_L.001.L", new Quaternion(0.01086195f, 0.0036522183f, 0.02658209f, 0.999581f) },
            { "ring_L.002.L", new Quaternion(0.015079821f, -0.007659279f, 0.023724169f, 0.99957544f) },
            { "ring_L.003.L", new Quaternion(0.065022476f, 0.018571673f, 0.091601476f, 0.9934971f) },
            { "pink_L.L", new Quaternion(-0.020745242f, 0.04472973f, -0.024094911f, 0.998493f) },
            { "pink_L.003.L", new Quaternion(0.0032518147f, -0.005632303f, 0.030311305f, 0.9995194f) },
            { "pink_L.002.L", new Quaternion(0.007715171f, 0.07224656f, 0.018840352f, 0.99717903f) },
            { "pink_L.001.L", new Quaternion(-0.008693648f, 0.18993284f, -0.022216937f, 0.9815072f) },
        };
        public static bool TryGet(string name, out Quaternion rotation) => rotations.TryGetValue(name, out rotation);
    }
}
