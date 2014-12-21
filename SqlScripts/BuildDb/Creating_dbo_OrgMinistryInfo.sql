CREATE FUNCTION [dbo].[OrgMinistryInfo](
	 @oid INT
	,@grouptype VARCHAR(20)
	,@first VARCHAR(30)
	,@last VARCHAR(30)
	,@sgfilter VARCHAR(300)
	,@showhidden BIT
) RETURNS TABLE
AS
RETURN
(
	SELECT 
		 tt.Tab
		,tt.GroupCode
		,tt.PeopleId
		,mi.LastContactMadeDt
		,mi.LastContactMadeId
		,mi.LastContactReceivedDt
		,mi.LastContactReceivedId
		,mi.TaskAboutDt
		,mi.TaskAboutId
		,mi.TaskDelegatedDt
		,mi.TaskDelegatedId
		
	FROM 
	dbo.OrgPeople(@oid, @grouptype, @first,@last,@sgfilter,@showhidden, NULL, NULL) tt
	JOIN dbo.People p ON p.PeopleId = tt.PeopleId
	JOIN dbo.MinistryInfo mi ON mi.PeopleId = p.PeopleId
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
