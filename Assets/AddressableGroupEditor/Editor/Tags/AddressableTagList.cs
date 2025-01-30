using Sirenix.OdinInspector;

namespace ARK.EditorTools.CustomAttribute
{
    using UnityEngine;
    using System.Collections.Generic;

    public class AddressableTagList : ScriptableObject
    {
        [AddressableLabelsTag,HorizontalGroup("addressable")]
        public List<string> tags;

        [Button,HorizontalGroup("addressable",Width = 50)]
        public void Test()
        {
            foreach(var tag in tags)
            {
                Debug.Log(tag);
            }
        }
        
    }

}