import 'package:flutter/foundation.dart';

/// Base class for ViewModels: view-facing state and behavior, holding no
/// widget/BuildContext references so it can be unit tested without Flutter's
/// widget tree.
abstract class ViewModel extends ChangeNotifier {
  bool _disposed = false;

  /// True once the View has torn this ViewModel down. Long operations check
  /// it after an `await` to skip follow-up work nobody will see, such as a
  /// reload after a save.
  @protected
  bool get isDisposed => _disposed;

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  /// Guards against notifying after dispose, which `ChangeNotifier` treats as
  /// an error — relevant here because a ViewModel's async work can finish
  /// after its View has already been torn down.
  @protected
  void notifyIfActive() {
    if (!_disposed) {
      notifyListeners();
    }
  }
}
