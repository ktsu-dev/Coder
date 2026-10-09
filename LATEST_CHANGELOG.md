## v3.18.16 (patch)

Changes since v3.18.15:

- fix: report an empty document as empty rather than unparseable [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: clone a variable reference that has no name yet [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: register each generator once when AddCoder is called twice [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: give a C struct with no data a placeholder member [patch] ([@Claude](https://github.com/Claude))
- fix: write a Go constant holding NaN or an infinity as a var [patch] ([@Claude](https://github.com/Claude))
- fix: generate an empty enum that compiles in C and Rust [patch] ([@Claude](https://github.com/Claude))

