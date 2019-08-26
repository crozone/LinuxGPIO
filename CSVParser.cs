using System;
using System.Collections.Generic;
using System.Text;

namespace crozone.LinuxGpio
{
    public static class CSVParser
    {
        private enum ReadState
        {
            WaitingForNewField,
            InsideNormal,
            InsideQuotes,
        }

        private const char DoubleQuote = '"';
        private const char Comma = ',';

        /// <summary>
        /// Parses the CSV line, returning it column by column as a sequence.
        /// Uses ',' as the default column separator.
        /// </summary>
        /// <param name="line">The input line</param>
        /// <returns>A sequence of each field</returns>
        public static IEnumerable<string> ParseLine(string line)
        {
            return ParseLine(line, Comma);
        }

        /// <summary>
        /// Parses the CSV line, returning it column by column as a sequence.
        /// </summary>
        /// <param name="line">The input line</param>
        /// <param name="separator">The separator between each field</param>
        /// <returns>A sequence of each field</returns>
        public static IEnumerable<string> ParseLine(string line, char separator)
        {
            StringBuilder currentValue = new StringBuilder();
            char currentChar;
            char? nextChar = null;
            ReadState currentState = ReadState.WaitingForNewField;

            for (var charIndex = 0; charIndex < line.Length; charIndex++)
            {
                // Get the current and next character
                //
                currentChar = line[charIndex];
                nextChar = charIndex < line.Length - 1 ? line[charIndex + 1] : new Nullable<char>();

                // Perform logic based on state and decide on next state
                //
                switch (currentState)
                {
                    case ReadState.WaitingForNewField:
                        {
                            currentValue = new StringBuilder();
                            if (currentChar == DoubleQuote)
                            {
                                // Transition into a quoted section
                                //
                                currentState = ReadState.InsideQuotes;
                                continue;
                            }
                            else if (currentChar == separator)
                            {
                                yield return currentValue.ToString();

                                // We're still in a separator
                                //
                                currentState = ReadState.WaitingForNewField;
                                continue;
                            }
                            else
                            {
                                // We're in a normal field, push the current character
                                //
                                currentValue.Append(currentChar);
                                currentState = ReadState.InsideNormal;
                                continue;
                            }
                        }
                    case ReadState.InsideNormal:
                        {
                            // Handle field content
                            //
                            if (currentChar == separator)
                            {
                                // We hit the other side of the field, return it
                                //
                                currentState = ReadState.WaitingForNewField;
                                yield return currentValue.ToString();
                                currentValue = new StringBuilder();
                                continue;
                            }

                            // Handle double quote escaping
                            //
                            if (currentChar == DoubleQuote && nextChar == DoubleQuote)
                            {
                                // Advance 1 character now. The next loop will advance one more.
                                //
                                currentValue.Append(DoubleQuote);
                                charIndex++;
                                continue;
                            }

                            // Add the current char to the current value
                            //
                            currentValue.Append(currentChar);
                            break;
                        }
                    case ReadState.InsideQuotes:
                        {
                            // Handle field content delimiter by ending double quotes
                            //
                            if (currentChar == DoubleQuote && nextChar != DoubleQuote)
                            {
                                currentState = ReadState.InsideNormal;
                                continue;
                            }

                            // Handle double quote escaping
                            //
                            if (currentChar == DoubleQuote && nextChar == DoubleQuote)
                            {
                                // Advance 1 character now. The loop will advance one more.
                                //
                                currentValue.Append(DoubleQuote);
                                charIndex++;
                                continue;
                            }

                            currentValue.Append(currentChar);
                            break;
                        }
                }
            }

            // Return anything yet to be returned.
            //
            yield return currentValue.ToString();
        }
    }

}
