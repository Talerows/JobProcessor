UPDATE jobs
SET started_at = NULL, completed_at = NULL, status = 'Open'
WHERE status = 'InProgress';
UPDATE jobs
SET started_at = NULL, timed_out_at = NULL, status = 'Open'
WHERE status = 'Timeout';
UPDATE jobs
SET started_at = NULL, completed_at = NULL, status = 'Open'
WHERE status = 'Completed';
select * from jobs;