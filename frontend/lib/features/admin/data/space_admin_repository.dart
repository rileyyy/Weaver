import 'package:weaver/shared/models/space.dart';

/// Creating and renaming Spaces. Anyone signed in can list them; each
/// change needs a human admin, and the server answers 403 otherwise. A name
/// another Space has, ignoring case, is refused with a 409.
abstract class SpaceAdminRepository {
  Future<List<Space>> list();

  Future<Space> create(String name);

  Future<Space> rename(String id, String name);
}
