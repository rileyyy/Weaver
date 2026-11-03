/// The lookups the Admin page manages besides statuses, with where each
/// lives on the API and how the page talks about it.
enum LookupKind {
  priorities(
    path: '/work-item-priorities',
    singular: 'priority',
    hasColor: false,
    ownedBySpace: true,
  ),
  layers(
    path: '/work-item-layers',
    singular: 'layer',
    hasColor: true,
    ownedBySpace: false,
  );

  const LookupKind({
    required this.path,
    required this.singular,
    required this.hasColor,
    required this.ownedBySpace,
  });

  final String path;
  final String singular;

  /// Whether each value also has a `#RRGGBB` colour, sent with its name.
  final bool hasColor;

  /// Whether each Space has its own values (ADR-0001), rather than every
  /// Space sharing one list.
  final bool ownedBySpace;
}
