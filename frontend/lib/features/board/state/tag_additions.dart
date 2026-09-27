/// Mirrors `WorkItem.TagMaxLength` on the backend.
const int tagMaxLength = 50;

/// [existing] followed by every tag in [additions] it doesn't already have,
/// trimmed. Blank additions are skipped and duplicates are matched ignoring
/// case, keeping the first spelling — the same normalisation the server
/// applies, so the optimistic update shows what the server will store.
List<String> withTagsAdded(List<String> existing, Iterable<String> additions) {
  final seen = {for (final tag in existing) tag.toLowerCase()};
  return [
    ...existing,
    for (final tag in additions.map((t) => t.trim()))
      if (tag.isNotEmpty && seen.add(tag.toLowerCase())) tag,
  ];
}

/// The tags from [available] worth offering for a quick add: those not in
/// [excluded] (ignoring case) whose text contains [query] (ignoring case;
/// an empty query matches everything), in [available]'s order.
List<String> tagSuggestions(
  List<String> available, {
  required List<String> excluded,
  String query = '',
}) {
  final excludedKeys = {for (final tag in excluded) tag.toLowerCase()};
  final needle = query.trim().toLowerCase();
  return [
    for (final tag in available)
      if (!excludedKeys.contains(tag.toLowerCase()) &&
          tag.toLowerCase().contains(needle))
        tag,
  ];
}
