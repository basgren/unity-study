using System;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Core.Models {
    [CreateAssetMenu(menuName = "Defs/DefsFacade", fileName = "DefsFacade")]
    public class DefsFacade : ScriptableObject {
        private static DefsFacade instance;

        [SerializeField]
        private InventoryItemsDef items;

        public static DefsFacade I {
            get {
                if (instance == null) {
                    instance = LoadDefs();
                }

                return instance;
            }
        }

        public InventoryItemsDef Items => items;
        
        private static DefsFacade LoadDefs() {
            var loaded = Resources.Load<DefsFacade>("DefsFacade");
            
            if (loaded == null) {
                throw new Exception("No DefsFacade found in Resources/DefsFacade.asset");
            }
            
            return loaded;
        }
    }
}
