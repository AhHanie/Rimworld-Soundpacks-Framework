using System;
using System.Collections.Generic;
using System.Globalization;

namespace Soundpacks_Framework.Serialization.Json
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    public sealed class JsonValue
    {
        public JsonKind Kind { get; private set; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonValue> _array;
        private List<KeyValuePair<string, JsonValue>> _object;

        public static readonly JsonValue Null = new JsonValue { Kind = JsonKind.Null };

        public static JsonValue Of(bool value) => new JsonValue { Kind = JsonKind.Bool, _bool = value };
        public static JsonValue Of(double value) => new JsonValue { Kind = JsonKind.Number, _number = value };
        public static JsonValue Of(int value) => new JsonValue { Kind = JsonKind.Number, _number = value };
        public static JsonValue Of(string value) =>
            value == null ? Null : new JsonValue { Kind = JsonKind.String, _string = value };

        public static JsonValue NewArray() => new JsonValue { Kind = JsonKind.Array, _array = new List<JsonValue>() };
        public static JsonValue NewObject() => new JsonValue { Kind = JsonKind.Object, _object = new List<KeyValuePair<string, JsonValue>>() };

        public bool IsNull => Kind == JsonKind.Null;

        public bool AsBool(bool fallback = false) => Kind == JsonKind.Bool ? _bool : fallback;
        public double AsNumber(double fallback = 0) => Kind == JsonKind.Number ? _number : fallback;
        public int AsInt(int fallback = 0) => Kind == JsonKind.Number ? (int)_number : fallback;
        public string AsString(string fallback = null) => Kind == JsonKind.String ? _string : fallback;

        public IReadOnlyList<JsonValue> ArrayItems => _array ?? (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();

        public IReadOnlyList<KeyValuePair<string, JsonValue>> ObjectMembers =>
            _object ?? (IReadOnlyList<KeyValuePair<string, JsonValue>>)Array.Empty<KeyValuePair<string, JsonValue>>();

        public void Add(JsonValue item)
        {
            if (Kind != JsonKind.Array)
            {
                throw new InvalidOperationException("Add(item) requires an array JsonValue.");
            }
            _array.Add(item);
        }

        public void Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object)
            {
                throw new InvalidOperationException("Set(key, value) requires an object JsonValue.");
            }
            for (int i = 0; i < _object.Count; i++)
            {
                if (_object[i].Key == key)
                {
                    _object[i] = new KeyValuePair<string, JsonValue>(key, value);
                    return;
                }
            }
            _object.Add(new KeyValuePair<string, JsonValue>(key, value));
        }

        public bool TryGet(string key, out JsonValue value)
        {
            if (Kind == JsonKind.Object)
            {
                for (int i = 0; i < _object.Count; i++)
                {
                    if (_object[i].Key == key)
                    {
                        value = _object[i].Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        public JsonValue Get(string key)
        {
            return TryGet(key, out var value) ? value : Null;
        }

        public bool Has(string key) => TryGet(key, out _);

        public bool Remove(string key)
        {
            if (Kind != JsonKind.Object)
            {
                return false;
            }
            for (int i = 0; i < _object.Count; i++)
            {
                if (_object[i].Key == key)
                {
                    _object.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public JsonValue DeepClone()
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    return Null;
                case JsonKind.Bool:
                    return Of(_bool);
                case JsonKind.Number:
                    return Of(_number);
                case JsonKind.String:
                    return Of(_string);
                case JsonKind.Array:
                    var array = NewArray();
                    foreach (var item in _array)
                    {
                        array.Add(item.DeepClone());
                    }
                    return array;
                case JsonKind.Object:
                    var obj = NewObject();
                    foreach (var member in _object)
                    {
                        obj.Set(member.Key, member.Value.DeepClone());
                    }
                    return obj;
                default:
                    throw new InvalidOperationException("Unknown JsonKind: " + Kind);
            }
        }

        public string ToInvariantString()
        {
            return Kind == JsonKind.Number
                ? _number.ToString(CultureInfo.InvariantCulture)
                : (_string ?? string.Empty);
        }
    }
}
