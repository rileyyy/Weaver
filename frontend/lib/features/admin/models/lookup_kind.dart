/// The name-only lookups the Admin page manages, with where each lives on
/// the API and how the page talks about it.
enum LookupKind {
  priorities(path: '/work-item-priorities', singular: 'priority'),
  layers(path: '/work-item-layers', singular: 'layer');

  const LookupKind({required this.path, required this.singular});

  final String path;
  final String singular;
}
