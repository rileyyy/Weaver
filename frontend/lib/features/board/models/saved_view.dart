import 'package:weaver/features/board/models/view_definition.dart';

/// A View (`ViewDto`): a saved, shared query and layout, or the built-in
/// Overview. Named SavedView because Flutter already has a `View` widget.
class SavedView {
  const SavedView({
    required this.id,
    required this.definition,
    this.builtIn = false,
  });

  factory SavedView.fromJson(Map<String, dynamic> json) => SavedView(
    id: json['id'] as String,
    builtIn: json['builtIn'] as bool? ?? false,
    definition: ViewDefinition.fromJson(json),
  );

  final String id;

  /// True for Overview, which can't be renamed, changed or deleted.
  final bool builtIn;
  final ViewDefinition definition;

  String get name => definition.name;
}
