using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InfinityPBR.Demo
{
    public class SerpentWarriorDemo : InfinityDemoCharacter
    {
        [Header("Texture Buttons")]
        public Button[] bodyButtons;
        public Button[] gearButtons;
        public Toggle[] bodyParts;
        
        public void RandomizeTextures(){
            bodyButtons[Random.Range(0, bodyButtons.Length)].onClick.Invoke();
            gearButtons[Random.Range(0, gearButtons.Length)].onClick.Invoke();
            foreach (var obj in bodyParts)
            {
                obj.isOn = Random.Range(0f, 1f) > 0.5f;
                obj.onValueChanged.Invoke(obj.isOn);
            }
        }
    }
}