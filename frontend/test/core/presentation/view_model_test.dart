import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/presentation/view_model.dart';

class _TestViewModel extends ViewModel {
  bool get disposed => isDisposed;

  void changed() => notifyIfActive();
}

void main() {
  test('notifyIfActive notifies listeners while active', () {
    final viewModel = _TestViewModel();
    var notifications = 0;
    viewModel
      ..addListener(() => notifications++)
      ..changed();

    expect(notifications, 1);
    expect(viewModel.disposed, isFalse);
  });

  test('notifyIfActive after dispose is a silent no-op', () {
    final viewModel = _TestViewModel()..dispose();

    expect(viewModel.changed, returnsNormally);
    expect(viewModel.disposed, isTrue);
  });
}
