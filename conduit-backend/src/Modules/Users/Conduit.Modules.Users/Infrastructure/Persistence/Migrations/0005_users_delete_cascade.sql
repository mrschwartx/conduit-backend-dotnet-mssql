CREATE TRIGGER users_delete_cascade ON users
INSTEAD OF DELETE
AS
BEGIN
  SET NOCOUNT ON;
  DELETE FROM comments WHERE author_id IN (SELECT id FROM deleted);
  DELETE FROM favorites WHERE user_id IN (SELECT id FROM deleted);
  DELETE FROM follows WHERE followed_id IN (SELECT id FROM deleted);
  DELETE FROM users WHERE id IN (SELECT id FROM deleted);
END
