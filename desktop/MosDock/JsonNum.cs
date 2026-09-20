using System.Text.Json;

namespace MosDock;

// System.Text.Json's TryGetInt32/TryGetDouble throw when the element is
// null/string instead of returning false. The portal sends null for
// time_limit_sec / remaining_sec in Training and elapsed-only banks, so every
// numeric read goes through these kind-checked variants.
static class JsonNum
{
    public static bool TryInt(this JsonElement el, out int value)
    {
        value = 0;
        return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value);
    }

    public static bool TryDouble(this JsonElement el, out double value)
    {
        value = 0;
        return el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value);
    }
}
