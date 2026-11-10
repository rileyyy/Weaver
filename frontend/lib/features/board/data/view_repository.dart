import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';

/// The shared Views and the signed-in user's default.
abstract class ViewRepository {
  /// Overview first, then every saved View by name.
  Future<List<SavedView>> list();

  Future<SavedView> create(ViewDefinition definition);

  /// Replaces [id]'s whole definition, its name included.
  Future<SavedView> update(String id, ViewDefinition definition);

  Future<void> delete(String id);

  /// The signed-in user's default View; Overview until they pick another.
  Future<SavedView> loadDefault();

  /// Overview's id clears the choice.
  Future<SavedView> setDefault(String id);
}
