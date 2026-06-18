UPDATE orders
SET started_at = NULL, finished_at = NULL, status = 'Open'
WHERE status = 'InProgress';
UPDATE orders
SET started_at = NULL, timeout_at = NULL, status = 'Open'
WHERE status = 'Timeout';
UPDATE orders
SET started_at = NULL, finished_at = NULL, status = 'Open'
WHERE status = 'Completed';
select * from orders;