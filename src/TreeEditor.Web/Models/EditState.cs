namespace TreeEditor.Web.Models;

public sealed class EditState
{
    public Guid? Key { get; set; }
    public string Value { get; set; } = "";
    public bool NeedsFocus { get; set; }
}
