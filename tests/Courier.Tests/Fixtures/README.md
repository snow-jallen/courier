# Test fixtures

`private/` is gitignored and holds real LCR exports. They carry home addresses,
phone numbers and birthdays for hundreds of people and must never be committed —
nor must anything derived from one. Fixtures that are committed are synthetic and
built in code, by `SyntheticReport`, `SyntheticCallingsReport` and
`SyntheticMemberList`.

The tests that need a real export skip themselves when it is absent, so the suite
passes on a machine that has never seen the directory.

| File | Report | Export it from |
|------|--------|----------------|
| `private/manti-singles.pdf`  | Single Adults              | LCR → Single Adults → print to PDF |
| `private/manti-callings.pdf` | Organizations and Callings | LCR → Organizations and Callings → print to PDF |
| `private/manti-member-list.pdf` | Member List             | LCR → Membership → Member List → print to PDF |

Delete them when you are finished with them.
