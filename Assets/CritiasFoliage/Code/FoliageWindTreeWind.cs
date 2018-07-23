using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public class FoliageWindTreeWind : MonoBehaviour
    {
        private void OnDisable()
        {
            Debug.LogWarning("Foliage wind object [" + name + "] disabled! Must be enabled at runtime if you require wind!");
        }
    }
}
