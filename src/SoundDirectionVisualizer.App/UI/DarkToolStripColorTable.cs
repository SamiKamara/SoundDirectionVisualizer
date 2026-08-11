namespace SoundDirectionVisualizer.App.UI;

internal sealed class DarkToolStripColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => DarkUiTheme.CardBackground;
    public override Color ImageMarginGradientBegin => DarkUiTheme.CardBackground;
    public override Color ImageMarginGradientMiddle => DarkUiTheme.CardBackground;
    public override Color ImageMarginGradientEnd => DarkUiTheme.CardBackground;
    public override Color MenuBorder => DarkUiTheme.Border;
    public override Color MenuItemBorder => DarkUiTheme.AccentPressed;
    public override Color MenuItemSelected => DarkUiTheme.Selection;
    public override Color MenuItemSelectedGradientBegin => DarkUiTheme.Selection;
    public override Color MenuItemSelectedGradientEnd => DarkUiTheme.Selection;
    public override Color MenuItemPressedGradientBegin => DarkUiTheme.Selection;
    public override Color MenuItemPressedGradientMiddle => DarkUiTheme.Selection;
    public override Color MenuItemPressedGradientEnd => DarkUiTheme.Selection;
    public override Color SeparatorDark => DarkUiTheme.Border;
    public override Color SeparatorLight => DarkUiTheme.Border;
}
