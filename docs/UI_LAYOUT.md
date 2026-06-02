# PACS-Soft WinForms layout plan

## Monitor tab
- Real-time Dahua access-event feed in a black/green operator log.
- Door 1 and Door 2 manual controls: open, normal mode, always open, always closed.
- Emergency open-all button and FIRE ALARM simulation button.
- Top status strip shows MariaDB status, Dahua status, and daily statistics.

## Users tab
- Create/delete person workflows.
- Student creation with course and card binding.
- Assign card by `uid`, with duplicate card protection delegated to the database unique key and service checks.
- Block/unblock student cards.
- Search profile by `card_id`.

## Logs tab
- Access event table with filters for `card_id`, `uid`, `reader_id`, and an implementation default of the last seven days.
- Failed attempts are persisted automatically for denied access and can be surfaced with another grid if operators need a dedicated view.
- Security events are persisted for fire/tamper/forced entry alerts.

## Admin tab
- Audit log viewer for admins/security roles.
- Manual connection status refresh.
