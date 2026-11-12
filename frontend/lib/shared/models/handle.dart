/// An account's `@` name for mentions in comments (see `Handle` on the
/// backend): lower-case letters and digits in words joined by single
/// hyphens, e.g. `claude-research`.
library;

/// The server's limit (`Handle.MaxLength`).
const maxHandleLength = 50;

/// One handle without its `@`, the same rule as the server's `Handle.Pattern`.
const handlePattern = '[a-z0-9]+(?:-[a-z0-9]+)*';

/// A handle as far as it's been typed: nothing yet, or a handle that may end
/// in the hyphen before its next word (`clau`, `claude-`). Built from
/// [handlePattern] so mention completion can't drift from the rule.
const partialHandlePattern = '(?:$handlePattern-?)?';

final _wholeHandle = RegExp('^$handlePattern\$');

/// Why [handle] isn't a valid handle, or null if it is. Checked here so the
/// admin sees the rule before a round trip; the server checks it again.
String? handleProblem(String handle) {
  if (handle.length > maxHandleLength) {
    return 'A handle can be at most $maxHandleLength characters.';
  }
  if (!_wholeHandle.hasMatch(handle)) {
    return 'Use lower-case letters and digits, in words joined by single '
        'hyphens (e.g. claude-research).';
  }
  return null;
}
