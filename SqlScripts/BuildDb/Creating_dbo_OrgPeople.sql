CREATE FUNCTION [dbo].[OrgPeople](
	 @oid INT
	,@grouptype VARCHAR(20)
	,@first VARCHAR(30)
	,@last VARCHAR(30)
	,@sgfilter VARCHAR(300)
	,@showhidden BIT
	,@currtag NVARCHAR(300)
	,@currtagowner INT
	,@filterchecked BIT
	,@filtertag BIT
	,@ministryinfo BIT
	,@userpeopleid INT
) RETURNS TABLE
AS
RETURN
(
	SELECT 
		tt.PeopleId
		,Tab
		,GroupCode
		,p.Name
		,p.Name2
		,p.Age
		,p.BirthDay
		,p.BirthMonth
		,p.BirthYear
		,p.IsDeceased
		,p.PrimaryAddress [Address]
		,p.PrimaryAddress2 Address2
		,p.PrimaryCity City
		,p.PrimaryState ST
		,p.PrimaryZip Zip
		,p.EmailAddress
		,p.HomePhone
		,p.CellPhone
		,p.WorkPhone
		,ms.Description MemberStatus
		,bfclass.LeaderId
		,bfclass.LeaderName
		,CAST(CASE WHEN tp.Id IS NULL THEN 0 ELSE 1 END AS BIT) HasTag
		,CAST(CASE WHEN otp.Id IS NULL THEN 0 ELSE 1 END AS BIT) IsChecked
		,AttPct
		,LastAttended
		,Joined
		,Dropped
		,InactiveDate
		,MemberCode
		,MemberType
		,CAST(Hidden AS BIT) Hidden
		,Groups
		,mi.LastContactMadeDt
		,mi.LastContactMadeId
		,mi.LastContactReceivedDt
		,mi.LastContactReceivedId
		,mi.TaskAboutDt
		,mi.TaskAboutId
		,mi.TaskDelegatedDt
		,mi.TaskDelegatedId
		
	FROM 
	(
		SELECT * FROM dbo.OrgPeopleCurrent(@oid) WHERE @grouptype LIKE '%10%' UNION
		SELECT * FROM dbo.OrgPeopleInactive(@oid) WHERE @grouptype LIKE '%20%' UNION
		SELECT * FROM dbo.OrgPeopleProspects(@oid, @showhidden) WHERE @grouptype LIKE '%40%' UNION
		SELECT * FROM dbo.OrgPeoplePending(@oid) WHERE @grouptype LIKE '%30%' UNION
		SELECT * FROM dbo.OrgPeopleGuests(@oid, @showhidden) WHERE @grouptype LIKE '%60%' UNION
		SELECT * FROM dbo.OrgPeoplePrevious(@oid) WHERE @grouptype LIKE '%50%'
	) tt
	JOIN dbo.People p ON p.PeopleId = tt.PeopleId
	JOIN lookup.MemberStatus ms ON ms.Id = p.MemberStatusId
	LEFT JOIN dbo.Organizations bfclass ON bfclass.OrganizationId = p.BibleFellowshipClassId
	LEFT JOIN Tag t ON t.Name = @currtag AND t.PeopleId = @currtagowner
	LEFT JOIN dbo.TagPerson tp ON tp.Id = t.Id AND tp.PeopleId = p.PeopleId
	LEFT JOIN Tag ot ON ot.Name = 'Org-'+CONVERT(VARCHAR, @oid) AND ot.PeopleId = @userpeopleid AND ot.TypeId = 10 -- OrgMembersTag
	LEFT JOIN dbo.TagPerson otp ON otp.Id = ot.Id AND otp.PeopleId = p.PeopleId
	LEFT JOIN dbo.MinistryInfo mi ON @ministryinfo = 1 AND mi.PeopleId = p.PeopleId

	WHERE (ISNULL(LEN(@first), 0) = 0 OR (p.FirstName LIKE (@first + '%') OR p.NickName LIKE (@first + '%')))
	AND (ISNULL(LEN(@last), 0) = 0 OR p.LastName LIKE (@last + '%') OR p.PeopleId = TRY_CONVERT(INT, @last))
	AND (ISNULL(@filterchecked, 0) = 0 OR otp.Id IS NOT NULL)
	AND (ISNULL(@filtertag, 0) = 0 OR tp.Id IS NOT NULL)
	AND 
	( 
		(ISNULL(LEN(@sgfilter), 0) > 0 AND @sgfilter NOT LIKE 'ALL:%' AND @sgfilter <> 'NONE'
			AND EXISTS(SELECT NULL FROM split(tt.Groups, CHAR(10)) mt
			    WHERE EXISTS(SELECT NULL FROM split(@sgfilter, ',') pf 
					WHERE pf.value NOT LIKE '-%' AND mt.Value LIKE (pf.Value + '%')
				)
			)
		)
		OR (@sgfilter LIKE 'ALL:%' 
			AND (SELECT COUNT(*) FROM split(SUBSTRING(@sgfilter, 5, 200), ',') pf
				 WHERE pf.value NOT LIKE '-%'
			) = (SELECT COUNT(*) FROM split(Groups, CHAR(10)) mt
				 JOIN split(SUBSTRING(@sgfilter, 5, 200), ',') pf ON  mt.Value LIKE (pf.Value + '%')
				 WHERE pf.value NOT LIKE '-%'
			)
		)
		OR (@sgfilter = 'NONE' AND LEN(ISNULL(Groups, '')) = 0)
		OR @sgfilter IS NULL -- no filter
		OR LEN(@sgfilter) = 0 -- filter is empty
		-- check to see if they are all exclusion small groups
		OR NOT EXISTS(SELECT NULL FROM split(@sgfilter, ',') pf WHERE pf.value NOT LIKE '-%')
	)
	AND (NOT EXISTS(SELECT NULL FROM split(@sgfilter, ',') pf WHERE pf.value LIKE '-%')
		OR (ISNULL(LEN(@sgfilter), 0) > 0 AND @sgfilter NOT LIKE 'ALL:%' AND @sgfilter <> 'NONE' 
			AND NOT EXISTS(SELECT NULL FROM split(Groups, CHAR(10)) mt
			    WHERE EXISTS(SELECT NULL FROM split(@sgfilter, ',') pf 
					WHERE pf.value LIKE '-%' AND mt.Value LIKE SUBSTRING(pf.Value,2,200) + '%'
				)
			)
		)
	)
	AND (NOT(@grouptype LIKE '%50%' AND @grouptype LIKE '%60%')
		 OR GroupCode = (SELECT MAX(GroupCode) 
				FROM 
				(
				SELECT GroupCode FROM dbo.OrgPeopleGuests(@oid, @showhidden) WHERE PeopleId = p.PeopleId UNION
				SELECT GroupCode FROM dbo.OrgPeoplePrevious(@oid) WHERE PeopleId = p.PeopleId
				) tabt)
	)
)

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
