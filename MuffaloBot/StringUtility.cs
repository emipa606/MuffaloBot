using System;
using System.Collections.Generic;

namespace MuffaloBot
{
    public static class StringUtility
    {
        public static string WithinChars(this string str, int amount)
        {
            if (str.Length <= amount)
            {
                return str;
            }

            var strBefore = str.Substring(0, (amount / 2) - 2);
            var strAfter = str.Substring(str.Length - (int)(Math.Round(amount / 2.0) - 3));
            return string.Concat(strBefore, " ... ", strAfter);
        }

        public static string CapitalizeFirst(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (char.IsUpper(str[0]))
            {
                return str;
            }

            if (str.Length == 1)
            {
                return str.ToUpper();
            }

            return char.ToUpper(str[0]) + str.Substring(1);
        }

        public static string MakeFieldSemiReadable(this string str)
        {
            var result = new List<char>();
            var chars = str.ToCharArray();
            result.Add(char.ToUpper(chars[0]));
            for (var i = 1; i < chars.Length; i++)
            {
                if (char.IsUpper(chars[i]))
                {
                    result.Add(' ');
                    result.Add(char.ToLower(chars[i]));
                }
                else
                {
                    result.Add(chars[i]);
                }
            }

            return new string(result.ToArray());
        }

        public static string ToStringSign(this float f)
        {
            if (f > 0)
            {
                return "+" + f;
            }

            return f.ToString();
        }
    }
}