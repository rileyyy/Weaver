import 'package:weaver/features/board/models/tree_list_row.dart';
import 'package:weaver/shared/models/space.dart';

/// The heading over one Space's trees while the Hierarchy or Roadmap shows
/// several Spaces.
class SpaceHeaderRow implements TreeListRow {
  const SpaceHeaderRow(this.space);

  final Space space;
}
