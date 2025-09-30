using System;
using System.Windows.Forms;
using static AdifUnicodeSubstitutionDemo.UnicodeSubstitution;

namespace AdifUnicodeSubstitutionDemo
{
    /**
     * <summary>
     *   Demonstrates a possible technique for including Unicode code points in ADIF.
     * </summary>
     */
    public partial class FrmDemo : Form
    {
        public FrmDemo()
        {
            InitializeComponent();
        }

        /**
         * <summary>
         *   Initializes the form and displays the test results.
         * </summary>
         * <param name="sender">
         *   The source of the event.
         * </param>
         * <param name="e">
         *   An object that contains no event data (sic).
         * </param>
         */
        private void FrmDemo_Load(object sender, EventArgs e)
        {
            try
            {
                Text += " " + Application.ProductVersion;

                string[] testStrings = new string[]
                {
                    // U+00E7  ç  Latin Small Letter C with Cedilla.
                    // U+00E9  é  Latin Small Letter E with Acute.
                    // U+1F300 🌀 Cyclone.
                    // U+1F3EF 🏯 Japanese Castle.

                    "",
                    "US-ASCII only.",
                    "Café François",
                    "abc🌀🏯123",
                    "🌀🏯",

                    // U+007E ~ Tilde is the highest US-ASCII character (7F is Delete and not allowed in ADIF).
                    // U+00A1 ¡ Inverted exclamation mark is the lowest printable character outside the US-ASCII range.
                    // U+00FF ÿ Latin Small Letter Y with diaeresis.
                    // U+0100 Ā Latin Capital Letter A with macron.
                    // U+FE4F ﹏ Wavy Low Line.

                    "~¡ÿĀ﹏",
                };

                foreach (string testString in testStrings)
                {
                    string fieldName = "COMMENT";

                    ExportString(
                        fieldName,
                        testString,
                        out string exportedFieldData,
                        out string exportedUnicodeSubstitutions,
                        out string exportedAdif);

                    ImportString(
                        fieldName,
                        exportedFieldData,
                        exportedUnicodeSubstitutions,
                        out string fieldData);

                    TxtOutput.Text += $"Value: \"{fieldData}\"\r\nExport: \"{exportedAdif}\"\r\n\r\n";

                    // Check that the round trip reproduced the original string.
                    System.Diagnostics.Debug.Assert(fieldData == testString);
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, "Exception Loading Form", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message, "Exception Closing Form", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
