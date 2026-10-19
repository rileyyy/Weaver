/// The lookups the Admin page manages besides statuses, with where each
/// lives on the API and how the page talks about it.
enum LookupKind {
  priorities(
    path: '/work-item-priorities',
    singular: 'priority',
    hasColor: false,
  ),
  layers(path: '/work-item-layers', singular: 'layer', hasColor: true);

  const LookupKind({
    required this.path,
    required this.singular,
    required this.hasColor,
  });

  final String path;
  final String singular;

  /// Whether each value also has a `#RRGGBB` colour, sent with its name.
  final bool hasColor;
}
