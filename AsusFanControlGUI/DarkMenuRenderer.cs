using System.Drawing;
using System.Windows.Forms;

namespace AsusFanControlGUI
{
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(62, 62, 64)))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 64);
        public override Color MenuItemBorder => Color.FromArgb(62, 62, 64);
        public override Color MenuBorder => Color.FromArgb(51, 51, 55);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 64);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color MenuStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuStripGradientEnd => Color.FromArgb(45, 45, 48);
        public override Color ToolStripDropDownBackground => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color SeparatorDark => Color.FromArgb(51, 51, 55);
        public override Color SeparatorLight => Color.FromArgb(51, 51, 55);
        public override Color CheckBackground => Color.FromArgb(62, 62, 64);
        public override Color CheckSelectedBackground => Color.FromArgb(62, 62, 64);
        public override Color CheckPressedBackground => Color.FromArgb(27, 27, 28);
    }
}
