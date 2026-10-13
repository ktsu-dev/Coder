## v3.19.3 (patch)

Changes since v3.19.2:

- Truncate integer /= and %= in Python and JavaScript the way the C family does [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- Write a JavaScript long literal past the safe-integer range as a BigInt [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- Annotate an inferred Rust local whose initialiser is a long or a float [patch] ([@Claude](https://github.com/Claude))
- Bind args in a C or C++ main that accepts arguments [patch] ([@Claude](https://github.com/Claude))
- Value-initialise a constructor initialiser that has no value [patch] ([@Claude](https://github.com/Claude))
- Parenthesise a struct-literal call receiver in a Go if condition [patch] ([@Claude](https://github.com/Claude))

