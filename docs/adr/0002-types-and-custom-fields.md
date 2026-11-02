# 2. Types with custom fields replace Layers

Status: accepted (2026-11-02)

## Context

A Layer (Project / Goal / Task) is just a coloured label. Different kinds of
work need different data: a renovation wants a budget and a contractor, and
a bug wants a URL to its PR. Riley wants every part of this to be
configurable by users, not by developers.

## Decision

- **Rename Layer to Type.** A Type is defined per Space (see
  [ADR-0001](0001-spaces.md)) and keeps its colour.
- **Each Type defines its own custom fields.** The field kinds are: text,
  Markdown, number, currency, date, checkbox, single select, multi-select,
  user and URL.
- **Number and currency fields can roll up.** It's an opt-in setting. When
  it's on, a parent shows the sum of that field over its descendants,
  computed when read, next to its own value.
- **Types don't constrain the tree.** Any Type can be nested under any other.
- **Admins manage Types and fields in the UI.** Agents can read Type and
  field definitions but can't change them (see
  [ADR-0004](0004-agent-accounts-and-api-keys.md)).

## Consequences

- Custom field values need storage that can change shape without a schema
  migration. The likely choice is a `jsonb` column validated against the
  Type's field definitions in the Application layer.
- Changing an item's Type, or moving it between Spaces, has to decide what
  happens to field values that don't exist on the target Type.
- Only sum roll-ups are supported. Other aggregates wait until something
  actually needs them.
