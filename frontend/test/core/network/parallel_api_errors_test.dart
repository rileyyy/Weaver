import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/parallel_api_errors.dart';

Future<ParallelWaitError<Object?, Object?>> _failureOf(
  Future<Object?> first,
  Future<Object?> second,
) async {
  try {
    await (first, second).wait;
  } on ParallelWaitError<Object?, Object?> catch (e) {
    return e;
  }
  throw StateError('Both futures succeeded');
}

void main() {
  test('returns when every failure is an API failure', () async {
    final failure = await _failureOf(
      Future.error(const ApiException('down')),
      Future.value(1),
    );

    expect(
      () => rethrowNonApiErrors(asyncErrorsOf(failure.errors)),
      returnsNormally,
    );
  });

  test('rethrows a failure that is not an API failure', () async {
    final failure = await _failureOf(
      Future.error(const ApiException('down')),
      Future.error(StateError('bug')),
    );

    expect(
      () => rethrowNonApiErrors(asyncErrorsOf(failure.errors)),
      throwsStateError,
    );
  });
}
