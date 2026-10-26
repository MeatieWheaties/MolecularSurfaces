using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MolecularSurfaces.Components
{
    public interface IInstance
    {
        Type ValueType { get; }
        public void SetValue(object value);
        public void Delete();
        public void Init();
    }

    public interface IInstance<T> : IInstance
    {
        public T Value { get; set; }
    }

    public static class Instance
    {
        public static TInstance Create<TInstance>(string name, object value) where TInstance : IInstance
        {
            var _result = new GameObject(name, typeof(TInstance)).GetComponent<TInstance>();
            _result.SetValue(value);
            _result.Init();
            return _result;
        }

        public static TInstance Create<TInstance>(this Transform container, string name, object value) where TInstance : Component, IInstance
        {
            var _result = new GameObject(name, typeof(TInstance)).GetComponent<TInstance>();
            _result.transform.SetParent(container, false);
            _result.SetValue(value);
            _result.Init();
            return _result;
        }

        public static TInstance Create<TInstance>(this Transform container, string name) where TInstance : Component, IInstance
        {
            var _result = new GameObject(name, typeof(TInstance)).GetComponent<TInstance>();
            _result.transform.SetParent(container, false);
            _result.Init();
            return _result;
        }
    }

    public class Instance<T> : MonoBehaviour, IInstance<T>
    {
        public Type ValueType { get; } = typeof(T);

        public void SetValue(object value)
        {
            Value = (T)value;
        }

        public Guid Guid { get; set; } = Guid.NewGuid();

        public virtual T Value { get; set; }

        public virtual bool Exists => Value != null;

        void Awake()
        {
            Init();
        }
        public virtual void Init() { }

        void Update() { OnUpdate(); }
        protected virtual void OnUpdate() { }

        public void Delete()
        {
#if UNITY_EDITOR
            DestroyImmediate(gameObject);
#else
            Destroy(gameObject);
#endif
        }
    }
}
