using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Core.Models;
public class CustomControlMapping
{
    public string OldControl { get; set; }
    public string NewControl { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    [JsonConverter(typeof(ConditionConverter))]
    public object Condition { get; set; }
    public Dictionary<string, string> AdditionalAttributes { get; set; }
}
public class Condition
{
    public string AttributeName { get; set; }
    public string AttributeValue { get; set; }
    public string NewControl { get; set; }
    public Dictionary<string, string> ConditionalAttributes { get; set; }

}
public class ConditionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(object);  // Will apply to object type (for Condition property)
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<Condition>>();  // Deserialize as List<Condition>
        }
        else if (token.Type == JTokenType.Object)
        {
            return token.ToObject<Condition>();  // Deserialize as a single Condition
        }

        return null;  // Fallback case
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();  // You can implement if you need serialization
    }

    private class AmountToWordResult
    {
        public string Output { get; set; }
        public bool ShouldSkip { get; set; }
        public string PreviousControlId { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(PreviousControlId);
        public ControlInfo PreviousControl { get; set; }
        public ControlInfo CurrentControl { get; set; }
    }
}