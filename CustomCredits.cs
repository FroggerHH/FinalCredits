using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FinalCredits
{
    [System.Serializable]
    public class CustomCreditsData
    {
        public List<Block> blocks = new();

        [System.Serializable]
        public class Block
        {
            public BlockText title = new()
            {
                size = 60,
                font = "Norsebold",
                text = "My Cool Title"
            };
            public BlockText contend = new()
            {
                size = 35,
                width = 400,
                font = "Default",
                text = "bla-bla-bla     ...          something     "
            };

            [System.Serializable]
            public class BlockText
            {
                public string text = "empty";
                public int size = 35;
                public float width = 400;
                public string font = "Default";
            }
        }

        public class UnityBlock : MonoBehaviour
        {
            public Text title;
            public Text contend;
        }
    }
    public class CustomCredits
    {
        public CustomCreditsData creditsData = new();
    }
}
