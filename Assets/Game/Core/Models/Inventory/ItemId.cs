using System;
using UnityEngine;

namespace Game.Models {
    [Serializable]
    public struct ItemId : IEquatable<ItemId> {
        [SerializeField]
        private string value;

        public string Value { get { return value; } }
        public bool IsEmpty { get { return string.IsNullOrEmpty(value); } }

        public ItemId(string value) {
            this.value = value;
        }

        public override string ToString() { return value; }

        public bool Equals(ItemId other) { return value == other.value; }
        public override bool Equals(object obj) { return obj is ItemId other && Equals(other); }
        public override int GetHashCode() { return value == null ? 0 : value.GetHashCode(); }

        public static implicit operator string(ItemId id) { return id.value; }
        public static implicit operator ItemId(string value) { return new ItemId(value); }
    }
}
