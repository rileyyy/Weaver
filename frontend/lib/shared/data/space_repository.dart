import 'package:weaver/shared/models/space.dart';

/// The Spaces anyone signed in can see. Creating and renaming them is the
/// Admin page's job (`SpaceAdminRepository`).
abstract class SpaceRepository {
  /// Every Space, by name.
  Future<List<Space>> loadSpaces();
}
