Troy Web \- Property Manager  
Entity analysis

1. Users  
   1. Role  
      1. Applicant  
      2. Property Manager  
      3. Extra  
         1. Admin  
   2. Notes  
      1. Use Asp.Net identity   
2. Properties (Multiple)  
   1. Name  
   2. Address  
   3. Extra  
      1. Picture  
   4. \[List of Units\]  
3. Unit (Residences)  
   1. Number  
   2. Bedrooms  
   3. Rent Amount  
   4. \[Unit Type\] (Active)  
      1. Does this mean Rented?  
   5. Extra  
      1. Picture  
      2. Notes  
      3. Reuse for history?  
4. Unity Type  
   1. Active  
   2. Inactive  
   3. Extra  
      1. Repair  
5. Applicant (Renter)  
   1. Name  
   2. Phone  
   3. Email  
   4. Current address  
   5. \[History\]  
   6. Note: use history for current address  
6. Applicant History  
   1. Address  
   2. Landlord name  
   3. Landlord Phone  
   4. Move In date  
   5. Move Out date  
7. Applications  
   1. \[Applicant\]  
   2. Summary  
   3. \[Review Status\]  
      1. HISTORY  
   4. Review notes  
8. Application Review Status  
   1. Draft  
   2. Submitted  
   3. Returned  
   4. Approved  
   5. Denied  
   6. Withdrawn  
9. Application Review Status HISTORY  
   1. Status  
   2. Date  
   3. Comment  
10. Lease  
    1. Unit Number  
       1. FK constraint?  
    2. Applicant ID  
    3. Start Date  
    4. End Date (assumed 12 month)  
    5. Notes  
       1. HISTORY