using System.Collections;
using System.Collections.Generic;
using Ren.Common;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Ren.MaidChan
{
    public class MaidController : SingletonMono<MaidController>
    {
        [ShowInInspector, ReadOnly]
        private SkinnedMeshRenderer[] renderers;

        private Animator animator;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            animator = GetComponent<Animator>();
        }
 
        public SkinnedMeshRenderer[] GetMeshRenderers
        {
            get
            {
                return renderers;
            }
        }
        [Button]
        public void StopAnim()
        {
            animator.speed = 0;
        }

        [Button]
        public void PlayAnim()
        {
            animator.speed = 1;
        }

        public void SetRenderer(bool active)
        {
            foreach (var item in renderers)
            {
                item.enabled = active;
            }
        }
    }
}
