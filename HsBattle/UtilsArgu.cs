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
                if (!_dictionary.TryGetValue(parameter, out values))
                {
                    return null;
                }

                return values;
            }
        }

        public bool Exists(string argueKey)
        {
            return this[argueKey] != null && this[argueKey].Count > 0;
        }

        public bool IsTrue(string argueKey)
        {
            CheckSingle(argueKey);
            return this[argueKey] != null && this[argueKey][0].Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public string Single(string argueKey)
        {
            CheckSingle(argueKey);
            if (this[argueKey] != null && !IsTrue(argueKey))
            {
                return this[argueKey][0];
            }

            return null;
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
                AddDict(key, value);
            }
        }

        private void SaveArgueKey()
        {
            if (_argueKey != null)
            {
                DictAddValue(_argueKey, "true");
                _argueKey = null;
            }
        }

        private void SaveArgueValue(string argueValue)
        {
            if (_argueKey != null)
            {
                AddDict(_argueKey, RemoveQuotes(argueValue));
                _argueKey = null;
            }
        }

        private void AddDict(string key, string value)
        {
            if (!_dictionary.ContainsKey(key))
            {
                _dictionary.Add(key, new Collection<string>());
            }

            _dictionary[key].Add(value);
        }

        private void DictAddValue(string key, string value)
        {
            if (_dictionary.ContainsKey(key))
            {
                throw new ArgumentException(string.Format("Argument {0} has already been defined", key));
            }

            _dictionary.Add(key, new Collection<string>());
            _dictionary[key].Add(value);
        }

        private void CheckSingle(string argueKey)
        {
            if (this[argueKey] != null && this[argueKey].Count > 1)
            {
                throw new ArgumentException(string.Format("{0} has been specified more than once, expecting single value", argueKey));
            }
        }
    }
}
