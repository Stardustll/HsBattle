using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace HsBattle
{
    public sealed class UtilsArgu
    {
        private readonly Dictionary<string, Collection<string>> _dictionary;
        private string _argueKey;
        private static UtilsArgu _instance;

        public UtilsArgu(IEnumerable<string> arguments)
        {
            _dictionary = new Dictionary<string, Collection<string>>();
            Regex regex = new Regex("^-{1,2}|^/|=|:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (string argument in arguments)
            {
                string[] argueArray = regex.Split(argument, 3);
                switch (argueArray.Length)
                {
                    case 1:
                        SaveArgueValue(argueArray[0]);
                        break;
                    case 2:
                        SaveArgueKey();
                        _argueKey = argueArray[1];
                        break;
                    case 3:
                        SaveArgueKey();
                        SaveAllArgueValue(argueArray[1], RemoveQuotes(argueArray[2]).Split(','));
                        break;
                }
            }

            SaveArgueKey();
        }

        public static UtilsArgu Instance
        {
            get
            {
                return _instance ?? (_instance = new UtilsArgu(Environment.GetCommandLineArgs()));
            }
        }

        public Collection<string> this[string parameter]
        {
            get
            {
                Collection<string> values;
                return _dictionary.TryGetValue(parameter, out values) ? values : null;
            }
        }

        public bool Exists(string argueKey)
        {
            Collection<string> values;
            return _dictionary.TryGetValue(argueKey, out values) && values != null && values.Count > 0;
        }

        public bool IsTrue(string argueKey)
        {
            CheckSingle(argueKey);
            Collection<string> values;
            return _dictionary.TryGetValue(argueKey, out values)
                && values != null
                && values.Count > 0
                && values[0].Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public string Single(string argueKey)
        {
            CheckSingle(argueKey);
            Collection<string> values;
            if (!_dictionary.TryGetValue(argueKey, out values) || values == null || values.Count == 0)
            {
                return null;
            }

            return values[0].Equals("true", StringComparison.OrdinalIgnoreCase) ? null : values[0];
        }

        private static string RemoveQuotes(string argueValue)
        {
            int index1 = argueValue.IndexOf('"');
            int index2 = argueValue.LastIndexOf('"');

            while (index1 != index2)
            {
                argueValue = argueValue.Remove(index1, 1);
                argueValue = argueValue.Remove(index2 - 1, 1);
                index1 = argueValue.IndexOf('"');
                index2 = argueValue.LastIndexOf('"');
            }

            return argueValue;
        }

        private void SaveAllArgueValue(string key, IEnumerable<string> valueList)
        {
            foreach (string value in valueList)
            {
                AddOrAppend(key, value);
            }
        }

        private void SaveArgueKey()
        {
            if (_argueKey != null)
            {
                if (_dictionary.ContainsKey(_argueKey))
                {
                    throw new ArgumentException(string.Format("Argument {0} has already been defined", _argueKey));
                }

                AddOrAppend(_argueKey, "true");
                _argueKey = null;
            }
        }

        private void SaveArgueValue(string argueValue)
        {
            if (_argueKey != null)
            {
                AddOrAppend(_argueKey, RemoveQuotes(argueValue));
                _argueKey = null;
            }
        }

        private void AddOrAppend(string key, string value)
        {
            Collection<string> values;
            if (!_dictionary.TryGetValue(key, out values))
            {
                values = new Collection<string>();
                _dictionary.Add(key, values);
            }

            values.Add(value);
        }

        private void CheckSingle(string argueKey)
        {
            Collection<string> values;
            if (_dictionary.TryGetValue(argueKey, out values) && values != null && values.Count > 1)
            {
                throw new ArgumentException(string.Format("{0} has been specified more than once, expecting single value", argueKey));
            }
        }
    }
}
