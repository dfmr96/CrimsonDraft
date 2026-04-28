using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [Serializable]
    public class DocumentPage
    {
        [LocalizableTextArea(10)]
        public LocalizableText Text;
        public LocalizableSprite Image;
        public LocalizableText Caption;

        // TODO Remove eventually
        [TextArea(10,30)]
        [FormerlySerializedAs("Text")]
        public string Text_DEPRECATED;
        [FormerlySerializedAs("Image")]
        public Sprite Image_DEPRECATED;
        [FormerlySerializedAs("ImageCaption")]
        public string Caption_DEPRECATED;
        //-------

        [FormerlySerializedAs("ChangeColor")]
        public bool ChangeImageColor;
        public Color ImageColor = Color.white;

#if UNITY_EDITOR
        public bool EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Text_DEPRECATED))
            {
                Text.Unlocalized = Text_DEPRECATED;
                Text_DEPRECATED = "";
                dirty = true;
            }
            if (!string.IsNullOrEmpty(Caption_DEPRECATED))
            {
                Caption.Unlocalized = Caption_DEPRECATED;
                Caption_DEPRECATED = "";
                dirty = true;
            }
            if (Image_DEPRECATED != null)
            {
                Image.Unlocalized = Image_DEPRECATED;
                Image_DEPRECATED = null;
                dirty = true;
            }

            return dirty;
        }
#endif
    }

    [CreateAssetMenu(menuName = "Horror Engine/Document")]
    public class DocumentData : Register
    {
        public LocalizableText Name;
        public LocalizableSprite Image;

        // TODO Remove eventually
        [FormerlySerializedAs("Name")]
        [HideInInspector]
        public string Name_DEPRECATED;
        [FormerlySerializedAs("Image")]
        [HideInInspector]
        public Sprite Image_DEPRECATED;
        // ----------
        
        [Space]
        public bool ShowImageOnRead = true;
        public bool ShowPageCount = true;

        [HideInInspector]
        public List<DocumentPage> Pages;

        [Header("Audio")]
        public AudioClip ShowClip;
        public AudioClip PageClip;
        public AudioClip CloseClip;

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;
            if (!string.IsNullOrEmpty(Name_DEPRECATED))
            {
                Name.Unlocalized = Name_DEPRECATED;
                Name_DEPRECATED = "";
                dirty = true;
            }

            if (Image_DEPRECATED != null)
            {
                Image.Unlocalized = Image_DEPRECATED;
                Image_DEPRECATED = null;
                dirty = true;
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }

        }
#endif
    }
}