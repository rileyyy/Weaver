import 'package:flutter/widgets.dart';

/// Opens the Admin page and completes when it's closed, so the caller can
/// reload anything an admin may have changed (statuses, users). Supplied by
/// the composition root, since features don't import each other.
typedef AdminPageOpener = Future<void> Function(BuildContext context);
