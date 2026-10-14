## v3.19.3 (patch)

Changes since v3.19.2:

- Project member names with Select in NamesOf (matt-edmondson)
- Write readonly only on a C# struct, and make its instance members readonly [patch] (matt-edmondson)
- Name a C++ getter get_count() when count() would clash with its field [patch] (matt-edmondson)
- Keep the StaticType overloads adjacent and use Assert.Contains (Claude)
- Promote mixed int and float operands to the wider type [patch] (Claude)
- Let a Python subclass without a constructor take its base's arguments [patch] (Claude)
- Bind a Rust parameter or self mut when the body calls a mutating method on it [patch] (Claude)
- Truncate integer /= and %= in Python and JavaScript the way the C family does [patch] (matt-edmondson)
- Write a JavaScript long literal past the safe-integer range as a BigInt [patch] (matt-edmondson)
- Annotate an inferred Rust local whose initialiser is a long or a float [patch] (Claude)
- Bind args in a C or C++ main that accepts arguments [patch] (Claude)
- Value-initialise a constructor initialiser that has no value [patch] (Claude)
- Parenthesise a struct-literal call receiver in a Go if condition [patch] (Claude)

