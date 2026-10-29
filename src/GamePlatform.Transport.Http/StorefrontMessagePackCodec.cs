using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MessagePack;
using GamePlatform.Features.Store;

namespace GamePlatform.Transport.Http
{
    internal sealed class StorefrontMessagePackCodec
    {
        public byte[] EncodeRequest() => new byte[] { 0x80 };

        public IReadOnlyList<StorefrontOffer> DecodeResponse(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 262144)
                throw new FormatException();
            var reader = new MessagePackReader(new ReadOnlyMemory<byte>(bytes));
            if (reader.ReadMapHeader() != 1 || reader.ReadString() != "offers")
                throw new FormatException();
            int count = reader.ReadArrayHeader();
            if (count > 100)
                throw new FormatException();
            var offers = new List<StorefrontOffer>(count);
            string? previous = null;
            for (int i = 0; i < count; i++)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                string? id = null, currency = null, availability = null;
                int version = 0;
                long price = 0, purchased = -1;
                int? limit = null;
                bool limitSeen = false;
                IReadOnlyList<StorefrontLine>? lines = null;
                int fields = reader.ReadMapHeader();
                if (fields != 7)
                    throw new FormatException();
                for (int j = 0; j < fields; j++)
                {
                    string key = reader.ReadString() ?? throw new FormatException();
                    if (!seen.Add(key))
                        throw new FormatException();
                    switch (key)
                    {
                        case "offerId":
                            id = Key(ref reader);
                            break;
                        case "offerVersion":
                            version = reader.ReadInt32();
                            break;
                        case "price":
                            ReadPrice(ref reader, out currency, out price);
                            break;
                        case "contents":
                            lines = ReadLines(ref reader);
                            break;
                        case "purchaseLimit":
                            limitSeen = true;
                            if (!reader.TryReadNil())
                                limit = reader.ReadInt32();
                            break;
                        case "purchasedCount":
                            purchased = reader.ReadInt64();
                            break;
                        case "availability":
                            availability = reader.ReadString();
                            break;
                        default:
                            throw new FormatException();
                    }
                }
                if (id == null || (previous != null && string.CompareOrdinal(previous, id) >= 0) ||
                    version < 1 || currency == null || price <= 0 || lines == null || !limitSeen ||
                    (limit.HasValue && limit <= 0) || purchased < 0 ||
                    (availability != "available" && availability != "limit_reached" && availability != "insufficient_balance"))
                    throw new FormatException();
                previous = id;
                offers.Add(new StorefrontOffer(id, version, currency, price, lines, limit, purchased, availability));
            }
            if (!reader.End)
                throw new FormatException();
            return offers.AsReadOnly();
        }

        private static string Key(ref MessagePackReader reader)
        {
            string? value = reader.ReadString();
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 128 ||
                !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant))
                throw new FormatException();
            return value;
        }

        private static void ReadPrice(ref MessagePackReader reader, out string? currency, out long quantity)
        {
            currency = null;
            quantity = 0;
            var seen = new HashSet<string>();
            if (reader.ReadMapHeader() != 2)
                throw new FormatException();
            for (int i = 0; i < 2; i++)
            {
                string key = reader.ReadString() ?? throw new FormatException();
                if (!seen.Add(key))
                    throw new FormatException();
                if (key == "currencyId")
                    currency = Key(ref reader);
                else if (key == "quantity")
                    quantity = reader.ReadInt64();
                else
                    throw new FormatException();
            }
        }

        private static IReadOnlyList<StorefrontLine> ReadLines(ref MessagePackReader reader)
        {
            int count = reader.ReadArrayHeader();
            if (count < 1 || count > 63)
                throw new FormatException();
            var lines = new List<StorefrontLine>(count);
            for (int i = 0; i < count; i++)
            {
                string? kind = null, resource = null, resourceName = null;
                long quantity = 0;
                var seen = new HashSet<string>();
                if (reader.ReadMapHeader() != 3)
                    throw new FormatException();
                for (int j = 0; j < 3; j++)
                {
                    string key = reader.ReadString() ?? throw new FormatException();
                    if (!seen.Add(key))
                        throw new FormatException();
                    switch (key)
                    {
                        case "kind":
                            kind = reader.ReadString();
                            break;
                        case "currencyId":
                        case "itemId":
                            resourceName = key;
                            resource = Key(ref reader);
                            break;
                        case "quantity":
                            quantity = reader.ReadInt64();
                            break;
                        default:
                            throw new FormatException();
                    }
                }
                if (quantity <= 0 || resource == null ||
                    !((kind == "currency" && resourceName == "currencyId") || (kind == "item_stack" && resourceName == "itemId")))
                    throw new FormatException();
                lines.Add(new StorefrontLine(kind!, resource, quantity));
            }
            return lines.AsReadOnly();
        }
    }
}
