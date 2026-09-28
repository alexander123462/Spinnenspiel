using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    public static class CameraBuilder
    {
        public static Camera Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            go.AddComponent<Spinnenspiel.SpiderCamera>();
            return cam;
        }

        public static void Bind(Camera cam, Transform spider)
        {
            var so = new SerializedObject(cam.GetComponent<Spinnenspiel.SpiderCamera>());

            SerializedProperty target = so.FindProperty("target");
            if (target == null)
            {
                Debug.LogError("[CameraBuilder] FindProperty lieferte null: target");
            }
            else
            {
                target.objectReferenceValue = spider;
            }

            SerializedProperty obstacleMask = so.FindProperty("obstacleMask");
            if (obstacleMask == null)
            {
                Debug.LogError("[CameraBuilder] FindProperty lieferte null: obstacleMask");
            }
            else
            {
                obstacleMask.intValue = ~(1 << 2);
            }

            SerializedProperty desiredDistance = so.FindProperty("desiredDistance");
            if (desiredDistance == null)
            {
                Debug.LogError("[CameraBuilder] FindProperty lieferte null: desiredDistance");
            }
            else
            {
                desiredDistance.floatValue = 1.0f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
