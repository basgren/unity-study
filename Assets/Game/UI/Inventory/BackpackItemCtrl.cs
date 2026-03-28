using Core.Models;
using Game.Core.Models.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Inventory {
    public class BackpackItemCtrl : MonoBehaviour {
        [SerializeField]
        private Image icon;
        
        [SerializeField]
        private TextMeshProUGUI text;

        [SerializeField]
        private GameObject selectionObject;

        private InventoryItem item;
        public InventoryItem Item => item;
        
        public void SetItem(InventoryItem invItem) {
            if (invItem == null) {
                item = null;
                gameObject.SetActive(false);
                return;
            }
            
            gameObject.SetActive(true);
            item = invItem;
            var id = item.id;
            Sprite sprite = DefsFacade.I.Items.Get(id).Icon;
            
            icon.sprite = sprite;
            text.text = item.count.ToString();
        }
        
        public void SetSelected(bool selected) {
            selectionObject.SetActive(selected);
        }
    }
}
