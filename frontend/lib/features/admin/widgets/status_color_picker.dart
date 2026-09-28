import 'package:flutter/material.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// A row of preset swatches plus a `#RRGGBB` field for any other colour.
/// The seeded statuses' colours are among the presets.
class StatusColorPicker extends StatelessWidget {
  const StatusColorPicker({
    super.key,
    required this.controller,
    required this.onChanged,
    this.enabled = true,
  });

  static const presets = [
    '#1E88E5',
    '#FB8C00',
    '#7B1FA2',
    '#43A047',
    '#E53935',
    '#00897B',
    '#FDD835',
    '#6D4C41',
    '#546E7A',
    '#D81B60',
  ];

  final TextEditingController controller;
  final VoidCallback onChanged;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final selected = controller.text.trim().toUpperCase();
    final outline = Theme.of(context).colorScheme.onSurface;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final hex in presets)
              Tooltip(
                message: hex,
                child: InkWell(
                  customBorder: const CircleBorder(),
                  onTap: enabled
                      ? () {
                          controller.text = hex;
                          onChanged();
                        }
                      : null,
                  child: Container(
                    width: 28,
                    height: 28,
                    decoration: BoxDecoration(
                      color: parseStatusColor(hex),
                      shape: BoxShape.circle,
                      border: hex == selected
                          ? Border.all(color: outline, width: 3)
                          : null,
                    ),
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 12),
        TextField(
          controller: controller,
          enabled: enabled,
          decoration: InputDecoration(
            labelText: 'Colour',
            hintText: '#RRGGBB',
            prefixIcon: Padding(
              padding: const EdgeInsets.all(12),
              child: CircleAvatar(
                radius: 8,
                backgroundColor:
                    parseStatusColor(controller.text) ?? Colors.transparent,
              ),
            ),
          ),
          onChanged: (_) => onChanged(),
        ),
      ],
    );
  }
}
