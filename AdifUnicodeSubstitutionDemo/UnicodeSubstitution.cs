using System;
using System.Globalization;
using System.Text;

namespace AdifUnicodeSubstitutionDemo
{
    /**
     * <summary>
     *   Provides a demonstration illustrating exporting and importing ADIF field values
     *   that include non-US-ASCII Unicode code points.<br/>
     *   <br/>
     *   This is done by creating a list of Unicode code point substitutions immediately
     *   after a field definition and before the start of another field or end-of-record.<br/>
     *   <br/>
     *   The code point values are offset by 0x80 to ensure that US-ASCII characters are
     *   not encoded.  They are hexadecimal to reduce the space taken.
     *   <br/>
     *   For example, "Café François" generates "&lt;COMMENT:13&gt;Caf? Fran?ois {{3=69,9=67}} "
     * </summary>
     * <remarks>
     *   An improvement would be to add a checksum of the field data to the start of the
     *   Unicode substitutions list to give a chance of detecting that the field data has
     *   not been modified incorrectly since the ADIF was exported (e.g. by a user editing
     *   the ADIF file in a text editor).
     *   <br/>
     * </remarks>
     */
    internal class UnicodeSubstitution
    {
        // Offset the code point values so that it is not possible to "cheat" by
        // encoding US-ASCII characters.  This ensures the maximum compatibility
        // with ADIF parsers that do support Unicode substitutions.
        const int CodePointOffset = -0x80;

        // These strings are used to delimit the Unicode substitution list.
        // Except for '<', they can be pretty much any US-ASCII printable characters that
        // are extremely unlikely to be exported by applications between field definitions.
        //
        // Pairs of left and right curly brackets would seem to fit into that category whilst
        // being relatively easy for worked developers to review visually.
        private static readonly string
            StartSubstitutions = "{{",
            EndSubstitutions = "}}";

        private static readonly UTF32Encoding UTF32Encoder = new UTF32Encoding();

        /**
         * <summary>
         *   Given a UTF-16 string, creates an ADIF field definition and Unicode code
         *   point substitution list (the <see cref="System.String"/> .NET type is UTF-16).
         * </summary>
         * <param name="fieldName">
         *   The field name e.g. "COMMENT"
         * </param>
         * <param name="valueToExport">
         *   The string to export e.g. "abc🌀🏯123"
         * </param>
         * <param name="exportedFieldData">
         *   The exported field value e.g. "abc??123"
         * </param>
         * <param name="exportedUnicodeSubstitutions">
         *   The exported Unicode substitutions e.g. "{{3=1F280,4=1F36F}}"
         * </param>
         * <param name="exportedCombinedValue">
         *   The full exported content e.g. "&lt;COMMENT:8&gt;abc??123 {{3=1F280,4=1F36F}}"
         * </param>
         */

        internal static void ExportString(
            string fieldName,
            string valueToExport,
            out string exportedFieldData,
            out string exportedUnicodeSubstitutions,
            out string exportedCombinedValue)
        {
            // A placeholder character is substitued in the ADIF field's data value at each point
            // where a non-US-ASCII character occurs.
            // Any single US-ASCII character allowed in the ADIF Specification can be used.
            //
            // A space (' ') or quesion mark ('?') would likely be a good choice, although more
            // adventurous code could try and use a similar character e.g replace a lower case
            // letter C cedilla ('ç') with a lower case US-ASCII letter C ('c').
            const char asciiPlaceHolderCharacter = '?';

            StringBuilder
                fieldValue = new StringBuilder(),           // ADIF export content.
                unicodeSubstitutions = new StringBuilder(); // ADIF Unicode code point substitutions.

            if (!IsUnicodeAllowed(fieldName))
            {
                // In the case of substitutions not being allowed, rather than just the code here,
                // it would be better to check that the field data does not contain any non-US-ASCII
                // albeit that would reduce performance.

                fieldValue.Append(valueToExport);
            }
            else
            {
                for (int i = 0; i < valueToExport.Length; i++)
                {
                    if (char.IsHighSurrogate(valueToExport[i]))
                    {
                        // This character is a surrogate pair, i.e. occupies two UTF-16 Chars.

                        fieldValue.Append(asciiPlaceHolderCharacter);
                        unicodeSubstitutions.Append(
                            string.Format(
                                "{0}={1},",
                                fieldValue.Length - 1,
                                (CodePointOffset + char.ConvertToUtf32(valueToExport, i)).ToString("X")));
                        i++;
                    }
                    else if (valueToExport[i] >= 0x80)
                    {
                        // This character occupies a single UTF-16 System.Char and is not a US-ASCII character.

                        fieldValue.Append(asciiPlaceHolderCharacter);

                        byte[] bytes = UTF32Encoder.GetBytes(valueToExport.ToCharArray(), i, 1);

                        unicodeSubstitutions.Append(
                            string.Format(
                                "{0}={1},",
                                fieldValue.Length - 1,
                                (CodePointOffset + BitConverter.ToInt32(bytes, 0)).ToString("X")));
                    }
                    else
                    {
                        // This character occupies a single UTF-16 System.Char and is a US-ASCII character.

                        fieldValue.Append(valueToExport[i]);
                    }
                }
            }

            StringBuilder adifField = new StringBuilder();

            adifField.AppendFormat(
                "<{0}:{1}>{2} ",
                fieldName,
                fieldValue.Length,
                fieldValue.ToString());

            if (unicodeSubstitutions.Length > 0)
            {
                unicodeSubstitutions.Length--; // Remove trailing comma.
                unicodeSubstitutions.Insert(0, StartSubstitutions);
                unicodeSubstitutions.Append(EndSubstitutions + " ");
                adifField.Append(unicodeSubstitutions);
            }

            exportedFieldData = fieldValue.ToString();
            exportedUnicodeSubstitutions = unicodeSubstitutions.ToString();
            exportedCombinedValue = adifField.ToString();
        }

        /**
         * <summary>
         *   Takes the value of an ADIF field and a list of Unicode substitutions that
         *   have been extracted by a suitable ADIF parser and recreates the original string
         *   as UTF-16 (the <see cref="System.String"/> .NET type is UTF-16).
         * </summary>
         * <param name="fieldName">
         *   The ADIF field name e.g. "COMMENT"
         * </param>
         * <param name="importedFieldData">
         *   The imported field value e.g. "abc??123"
         * </param>
         * <param name="importedUnicodeSubstitutions">
         *   The imported Unicode values e.g. "{{3=1F280,4=1F36F}}"
         * </param>
         * <param name="fieldData">
         *   The resulting imported string value e.g. "abc🌀🏯123"
         * </param>
         */

        internal static void ImportString(
            string fieldName,
            string importedFieldData,
            string importedUnicodeSubstitutions,
            out string fieldData)
        {
            try
            {
                StringBuilder importFieldData = new StringBuilder();

                if (importedUnicodeSubstitutions.Length <= StartSubstitutions.Length + EndSubstitutions.Length ||
                    !IsUnicodeAllowed(fieldName))
                {
                    // The field does not allow substitutions or none have been supplied.

                    importFieldData.Append(importedFieldData);
                }
                else
                {
                    // Remove the {{ and }} delimiters.
                    string unicodePairs = importedUnicodeSubstitutions.Substring(
                        StartSubstitutions.Length,
                        importedUnicodeSubstitutions.Length - EndSubstitutions.Length - StartSubstitutions.Length - 1);

                    string[] unicodeValues = unicodePairs.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    int startIndex = 0;

                    foreach (string unicodeValue in unicodeValues)
                    {
                        string[] parts = unicodeValue.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            if (int.TryParse(parts[0], out int index) &&
                                int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int utf32))
                            {
                                // Get the original Unicode code point as a byte array.
                                //byte[] unicodeBytes = BitConverter.GetBytes(utf32 - CodePointOffset);
                                byte[] unicodeBytes = BitConverter.GetBytes(utf32 - CodePointOffset);

                                // Convert the byte array into a UTF-16 string.
                                string unicodeString = UTF32Encoder.GetString(unicodeBytes);

                                // Append the section of the field data up to the substitution followed by the
                                // UTF16 string.
                                importFieldData.
                                    Append(importedFieldData.Substring(startIndex, index - startIndex)).
                                    Append(unicodeString);

                                startIndex = index + 1;
                            }
                        }
                    }
                    importFieldData.Append(importedFieldData.ToString().Substring(startIndex));
                }

                fieldData = importFieldData.ToString();
            }
            catch
            {
                // If anything goes wrong, return the field data without any substitutions.
                fieldData = importedFieldData;
            }
        }


        /**
         * <summary>List of ADIF fields that are allowed to contain Unicode characters.</summary>
         */
        private static readonly string[] UnicodeEnabledFields = new string[]
        {
            "ADDRESS",
            "COMMENT",
            "COUNTRY",
            "MY_ANTENNA",
            "MY_CITY",
            "MY_COUNTRY",
            "MY_NAME",
            "MY_POSTAL_CODE",
            "MY_RIG",
            "MY_SIG",
            "MY_SIG_INFO",
            "MY_STREET",
            "NAME",
            "NOTES",
            "QSLMSG",
            "QTH",
            "RIG",
            "SIG",
            "SIG_INFO",
        };

        /**
         * <summary>
         *   Determines if a field is allowed to contain non-US-ASCII Unicode characters.
         * </summary>
         * <remarks>
         *   NOTE: In real life, this would need to cope with APP_ and USERDEF fields, which is
         *   complex because their data types need to be checked.<br/>
         *   <br/>
         *   A simplified approach would be to have instead a list of fields that are NOT allowed to
         *   contain non-US-ASCII characters and assume that all others are allowed - not perfect but
         *   very much easier to implement.
         * </remarks>
         * <param name="fieldName">
         *   The field name e.g. "COMMENT"
         * </param>
         * <returns>
         *   true if the field is allowed to contain non-US-ASCII Unicode characters, false otherwise.
         * </returns>
         */
        private static bool IsUnicodeAllowed(string fieldName)
        {
            bool unicodeAllowed = false;

            foreach (string allowedField in UnicodeEnabledFields)
            {
                if (string.Compare(fieldName, allowedField, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    unicodeAllowed = true;
                    break;
                }
            }
            return unicodeAllowed;
        }   
    }
}
