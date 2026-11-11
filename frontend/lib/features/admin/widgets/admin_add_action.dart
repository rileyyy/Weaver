import 'package:flutter/foundation.dart';

/// The add button an Admin tab offers above its list. A null [onPressed]
/// shows the button disabled, e.g. while there is no list to add to.
class AdminAddAction {
  const AdminAddAction(this.label, {required this.onPressed});

  final String label;
  final VoidCallback? onPressed;
}
