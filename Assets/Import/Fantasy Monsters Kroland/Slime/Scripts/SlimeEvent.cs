using UnityEngine;

namespace kroland.fantasymonsters
{
    public class SlimeEvent : MonoBehaviour
    {
        public GameObject fingerPoint;
        public GameObject poisonPrefab;
        public void poison(){
            GameObject go = GameObject.Instantiate(poisonPrefab,fingerPoint.transform.position,this.gameObject.transform.rotation);
            go.transform.SetParent(this.gameObject.transform);
        }
    }
}