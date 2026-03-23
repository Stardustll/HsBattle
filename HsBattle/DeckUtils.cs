using System;
using System.Collections.Generic;

namespace HsBattle
{
    internal sealed class QueueDeckInfo
    {
        public long Id;
        public string DisplayName;
        public string Name;
    }

    internal static class DeckUtils
    {
        public static List<QueueDeckInfo> GetConstructedDecks()
        {
            List<QueueDeckInfo> decks = new List<QueueDeckInfo>();

            try
            {
                CollectionManager collectionManager = CollectionManager.Get();
                if (collectionManager == null)
                {
                    return decks;
                }

                SortedDictionary<long, CollectionDeck> sourceDecks = collectionManager.GetDecks();
                if (sourceDecks == null || sourceDecks.Count == 0)
                {
                    return decks;
                }

                foreach (KeyValuePair<long, CollectionDeck> pair in sourceDecks)
                {
                    if (!IsSelectableConstructedDeck(pair.Value))
                    {
                        continue;
                    }

                    decks.Add(new QueueDeckInfo
                    {
                        Id = pair.Key,
                        Name = GetDeckName(pair.Value, pair.Key),
                        DisplayName = BuildDeckDisplayName(pair.Value, pair.Key)
                    });
                }

                decks.Sort(delegate (QueueDeckInfo left, QueueDeckInfo right)
                {
                    int nameCompare = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
                    return nameCompare != 0 ? nameCompare : left.Id.CompareTo(right.Id);
                });
            }
            catch
            {
            }

            return decks;
        }

        public static string DescribeQueueDeck(long deckId)
        {
            if (deckId <= 0L)
            {
                return "自动（首个可用）";
            }

            try
            {
                CollectionManager collectionManager = CollectionManager.Get();
                CollectionDeck deck = collectionManager != null ? collectionManager.GetDeck(deckId) : null;
                return deck != null ? BuildDeckDisplayName(deck, deckId) : "卡组 #" + deckId.ToString();
            }
            catch
            {
                return "卡组 #" + deckId.ToString();
            }
        }

        private static bool IsSelectableConstructedDeck(CollectionDeck deck)
        {
            return deck != null
                && deck.IsConstructedDeck
                && !deck.IsBrawlDeck
                && !deck.IsDraftDeck
                && !deck.IsDuelsDeck
                && !deck.IsDeckTemplate;
        }

        private static string BuildDeckDisplayName(CollectionDeck deck, long deckId)
        {
            string name = GetDeckName(deck, deckId);
            string format = DescribeDeckFormat(deck);
            return string.IsNullOrEmpty(format) ? name : string.Format("{0} [{1}]", name, format);
        }

        private static string GetDeckName(CollectionDeck deck, long deckId)
        {
            string name = deck != null ? deck.Name : string.Empty;
            return string.IsNullOrWhiteSpace(name) ? "卡组 #" + deckId.ToString() : name.Trim();
        }

        private static string DescribeDeckFormat(CollectionDeck deck)
        {
            if (deck == null)
            {
                return string.Empty;
            }

            switch (deck.FormatType)
            {
                case PegasusShared.FormatType.FT_STANDARD:
                    return "标准";
                case PegasusShared.FormatType.FT_WILD:
                    return "狂野";
                case PegasusShared.FormatType.FT_TWIST:
                    return "幻变";
                case PegasusShared.FormatType.FT_CLASSIC:
                    return "经典";
                default:
                    return string.Empty;
            }
        }
    }
}
