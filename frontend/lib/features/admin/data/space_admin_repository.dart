import 'package:weaver/features/admin/models/space_history_entry.dart';
import 'package:weaver/shared/models/space.dart';

/// Creating, renaming and configuring Spaces. Anyone signed in can list them; each
/// change needs a human admin, and the server answers 403 otherwise. A name
/// another Space has, ignoring case, is refused with a 409.
abstract class SpaceAdminRepository {
  Future<List<Space>> list();

  Future<Space> create(String name);

  Future<Space> rename(String id, String name);

  /// Sets how many days a Done item stays on the board after its
  /// completion date. Refused (400) outside 1 to 3650 days.
  Future<Space> setDoneHidingPeriod(String id, int days);

  /// Every change to the Space, newest first. Unpaged: a Space changes only
  /// a handful of times.
  Future<List<SpaceHistoryEntry>> history(String id);
}
