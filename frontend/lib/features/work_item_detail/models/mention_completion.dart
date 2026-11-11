import 'package:flutter/widgets.dart';

/// An `@` mention being typed in a comment, up to the cursor, and the
/// handles it could be completed to. Mirrors the server's mention rule: an
/// `@` straight after a letter, digit, dot or another `@` isn't a mention,
/// so typing an email address suggests nothing.
class MentionCompletion {
  const MentionCompletion({required this.start, required this.query});

  static final _typing = RegExp(
    r'(?:^|[^\w.@])@([a-z0-9-]*)$',
    caseSensitive: false,
  );

  /// Where the `@` is in the text.
  final int start;

  /// What's been typed after the `@`, lower-cased.
  final String query;

  /// The mention being typed just before [cursor], or null if there isn't
  /// one (e.g. a space has ended it).
  static MentionCompletion? at(String text, int cursor) {
    if (cursor < 0 || cursor > text.length) return null;
    final match = _typing.firstMatch(text.substring(0, cursor));
    if (match == null) return null;
    final typed = match.group(1)!;
    return MentionCompletion(
      start: cursor - typed.length - 1,
      query: typed.toLowerCase(),
    );
  }

  /// The [handles] that start with what's been typed, in order.
  List<String> suggest(Iterable<String> handles) =>
      handles.where((handle) => handle.startsWith(query)).toList()..sort();

  /// [value] with the typed mention replaced by `@handle` and a space, and
  /// the cursor after that space.
  TextEditingValue apply(TextEditingValue value, String handle) {
    final end = start + 1 + query.length;
    final text = value.text;
    final hasSpaceAfter = end < text.length && text[end].trim().isEmpty;
    final inserted = hasSpaceAfter ? '@$handle' : '@$handle ';
    return TextEditingValue(
      text: text.replaceRange(start, end, inserted),
      selection: TextSelection.collapsed(offset: start + '@$handle '.length),
    );
  }
}
