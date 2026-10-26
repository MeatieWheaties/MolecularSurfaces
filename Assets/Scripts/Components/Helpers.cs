using MolecularSurfaces.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MolecularSurfaces.Helpers
{
    internal static class MonoBehaviourF
    {
        public static TComponent[] Container<TComponent>(this Transform transform, string containerName)
        {
            var container = transform.Find(containerName);
            if (container == null)
            {
                container = new GameObject(containerName).transform;
                container.SetParent(transform, false);
            }

            return container.GetComponentsInChildren<TComponent>(true);
        }

        public static TComponent[] Container<TComponent>(this Transform transform)
        {
            return transform.GetComponentsInChildren<TComponent>(true);
        }

        public static Transform Cursor(this Transform transform, string label)
        {
            var cursor = transform.Find(label);
            if (cursor == null)
            {
                cursor = new GameObject(label).transform;
                cursor.SetParent(transform, false);
            }

            return cursor.transform;
        }

        public static Transform Cursor(this Transform transform, string label, string icon)
        {
            var cursor = transform.Find(label);
            if (cursor == null)
            {
                cursor = new GameObject(label).transform;
#if UNITY_EDITOR
                EditorGUIUtility.SetIconForObject(cursor.gameObject, Resources.Load<Texture2D>("Gizmos\\Icons\\" + icon + ""));
#endif
                cursor.SetParent(transform, false);
            }

            return cursor.transform;
        }

        public static void Purge<TComponent>(this Transform transform) where TComponent : IInstance
        {
            foreach (var _transform in transform.GetComponentsInChildren<TComponent>(true))
            {
                _transform.Delete();
            }
        }

        public static void Insert(this Transform transform, Transform child)
        {
            child.SetParent(transform, false);
        }

        //Special method to trigger the creation of Cursors and Containers
        public static void Ping(this Transform transform) { var _ = transform; }
    }

    public static class RandomF
    {
        public static T GetRandom<T>(this IEnumerable<T> collection)
        {
            return collection.ToArray()[UnityEngine.Random.Range(0, collection.Count())];
        }

        private static readonly System.Random Rng = new System.Random();

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            var buffer = source.ToList();
            for (int i = buffer.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
            return buffer;
        }
    }
}
