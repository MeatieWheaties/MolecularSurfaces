using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MolecularSurfaces.Presentation
{
    [ExecuteAlways]
    internal class SlideInstance : MonoBehaviour
    {
        public bool Build;
        protected void _Build()
        {
            BuildSlide();
            Build = false;
        }

        private void Update()
        {
            if (Build)
            {
                _Build();
            }

            OnUpdate();
        }

        protected virtual void BuildSlide() { }

        protected virtual void OnUpdate() { }
    }
}
