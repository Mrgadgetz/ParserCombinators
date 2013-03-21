
module ParserCombinators.Json

type JsonValue =
    | JsonNull
    | JsonBoolean of bool
    | JsonString of string
    | JsonNumber of float
    | JsonArray of list<JsonValue>
    | JsonObject of Map<string, JsonValue>

open ParserCombinators.Core

// Parses a key name that matches /[_\w][_\w\d]*/
let KeyNameParser =
    let AnsiLetterParser = CharParserF (fun c -> let c' = System.Char.ToLower c in c' >= 'a' && c <= 'z')
    let DigitCharParser = CharParserF (fun c -> c >= '0' && c <= '9')
    parse {
        let! x = AnsiLetterParser <|> CharParser '_' 
        let! xs = Many (AnsiLetterParser <|> DigitCharParser <|> CharParser '_')
        return new System.String(x::xs |> List.toArray)
    }
    
/// Parses an escaped estring. See http://json.org/string.gif
let EscapedStringParser =
    let EscapedChars =
        [('t', '\t'); ('n', '\n'); ('r', '\r'); ('b', '\b'); ('f', '\f'); ('"', '"'); ('/', '/'); ('\\', '\\')]
        |> Map.ofList
        
    let NotEscapedCharParser = CharParserF (fun c -> not (c = '\\' || c = '"'))
    
    let EscapedUnicodeParser =
        parse {
            let! i = CharParser '\\' >>. (CharParser 'u' <|> CharParser 'U')
            let! h =
                CharParserF (fun c -> (c >= '0' && c <= '9') && (c >= 'a' && c <='f') && (c >= 'A' && c <='F'))
                |> Times 4
                |>> (fun cs -> new System.String(Array.ofList cs))
                |>> (fun s -> System.Int32.Parse(s, System.Globalization.NumberStyles.HexNumber))
                |>> System.Char.ConvertFromUtf32
                |>> Array.ofSeq
            return h.[0]
        }
    
    let EscapedCharsParser =
        parse {
            let! i = CharParser '\\'
            let! c = CharParserF EscapedChars.ContainsKey |>> System.Char.ToLower
            return EscapedChars.[c]
        }
        
    parse {
        let! xs = Many (NotEscapedCharParser <|> EscapedCharsParser)
        return new System.String(xs |> List.toArray)
    }
    |> Between (CharParser '"') (CharParser '"')
    |>> JsonString

// Parses a Json boolean /true|false/
let JsonBooleanParser = (StringParser "true" >>% true) <|> (StringParser "false" >>% false) |>> JsonBoolean

// Parses a Json null value /null/
let JsonNullParser = StringParser "null" >>% JsonNull

// Parses a Json value: null, boolean, number, string, array or object
let mutable JsonValueParser = preturn JsonNull

// Parses a Json key-value entry. Example: "name": "Santi"
let JsonKeyValueParser =
    Between (CharParser '"') (CharParser '"') KeyNameParser .>> WhiteSpaceParser
    .>> CharParser ':' .>> WhiteSpaceParser
    .>>. JsonValueParser

// Parses a Json array.
let JsonArrayParser =
    parse {
        let! x = JsonValueParser .>> WhiteSpaceParser
        let! xs = Many (CharParser ','  >>. WhiteSpaceParser >>. JsonValueParser .>> WhiteSpaceParser)
        return x::xs
    } <|> preturn []
    |> Between (CharParser '[' .>> WhiteSpaceParser) (CharParser ']')
    |>> JsonArray
    
// Parses a Json object.
let JsonObjectParser =
    parse {
        let! x = JsonKeyValueParser .>> WhiteSpaceParser
        let! xs = Many (CharParser ','  >>. WhiteSpaceParser >>. JsonKeyValueParser .>> WhiteSpaceParser)
        return x::xs
    } <|> preturn []
    |>> Map.ofList
    |> Between (CharParser '{' .>> WhiteSpaceParser) (CharParser '}')
    |>> JsonObject
    
JsonValueParser <-
    Choice [JsonNullParser;
            JsonBooleanParser;
            FloatParser |>> JsonNumber;
            EscapedStringParser;
            JsonArrayParser;
            JsonObjectParser] .>> WhiteSpaceParser
